<?php
    class ConX {

        private $conn;

        private $debug;
        private $debug_color;

        // escape table names character, placed around all tables name in any requests
        private $etn_s;
        private $etn_e;

        function __construct($dbname, $as="root", $pass="", $infos=false, $mysql_host="127.0.0.1", $charset="utf8") {
            if ($as) {
                $c = $infos ? $infos : "mysql:host=$mysql_host;charset=$charset";
                $this->conn = new PDO("$c;dbname=$dbname", $as, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
            } else $this->conn = new PDO($dbname, null, null);

            $this->debug = false;
            $this->etn_s = '`';
            $this->etn_e = '`';
        }

        function set_debug($is=true, $code_color="#FF0000") {
            $this->debug = true;
            $this->debug_color = $code_color;
            return $this;
        }

        function set_escape($char='`', $end=null) {
            $this->etn_s = $char;
            $this->etn_e = $end === null ? $char : $end;
            return $this;
        }

        function query($c) {
            return $this->conn->query($c);
        }

        function prepared($string, $data) {
            $indexed = $data == array_values($data);
            foreach ($data as $k => $v) {
                if (is_string($v))
                    $v = "'$v'";
                if ($v === null)
                    $v = "NULL";
                if ($indexed)
                    $string = preg_replace('/\?/', $v, $string, 1);
                else
                    $string = str_replace(":$k", $v, $string);
            }
            return $string;
        }

        function prepare_execute($string, $data) {
            if ($this->debug)
                echo "<code class=\"conx-debug\" style=\"color: $this->debug_color;\">".$this->prepared($string, $data)."</code><br>\n";

            $r = $this->conn->prepare($string);
            $r->execute($data);
            return $r;
        }

        function build_where($where, &$execute_array, &$execute_index='a') {
            if (empty($where))
                return "";

            $where_builder = "WHERE ";
            $sep = "";

            foreach ($where as $k => $v) {
                if (is_string($k) && strpos($k, "::") !== false)
                    $where_builder.= "$sep".str_replace("::", ".", $k);
                else
                    $where_builder.= "$sep$k";

                if (is_array($v)) {
                    $where_builder.= " IN (";
                    $sep_ = "";
                    foreach ($v as $v_) {
                        if (strpos($v_, "::") !== false)
                            $where_builder.= "$sep_".str_replace("::", ".", $v_);
                        else {
                            $where_builder.= "$sep_:$execute_index";
                            $execute_array[$execute_index++] = $v_;
                        }

                        $sep_ = ", ";
                    }
                    $where_builder.= ")";
                } else {
                    if (strpos($v, "::") !== false)
                        $where_builder.= "= ".str_replace("::", ".", $v);
                    else {
                        $where_builder.= "= :$execute_index";
                        $execute_array[$execute_index++] = $v;
                    }
                }

                $sep = " AND ";
            }

            return $where_builder;
        }

        function select($table, $value="*", $where=array(), $order=null, $distinct=true) {
            $execute_array = array();
            $execute_index = 'a';

            // SELECT ..
            $value_builder = empty($distinct) ? "SELECT " : "SELECT DISTINCT ";
            if ($value == "*" || empty($value))
                $value_builder.= "*";
            elseif (is_array($value))
                $value_builder.= join(", ", $value);
            else
                $value_builder.= $value;

            // FROM ..
            $table_builder = "FROM $this->etn_s".(is_array($table) ? join("$this->etn_e JOIN $this->etn_s", $table) : $table)."$this->etn_e";

            // WHERE ..
            $where_builder = $this->build_where($where, $execute_array, $execute_index);

            // ORDER BY ..
            $order_builder = empty($order) ? "" : "ORDER BY ";
            $sep = "";
            if (is_array($order))
                foreach ($order as $k => $v) {
                    $k = str_replace("::", ".", $k);
                    $order_builder.= "$sep$k $v";
                    $sep = ", ";
                }

            return $this->prepare_execute("$value_builder $table_builder $where_builder $order_builder", $execute_array);
        }

        function insert($table, $data, $multiple=null, $id_return=false) {
            $keywords_builder = "";
            $tuple_builder = "";
            $sep = "";
            $execute_array = array();

            if (!$multiple) {
                foreach ($data as $k => $v) {
                    $keywords_builder.= "$sep$k";
                    $tuple_builder.= "$sep:$k";
                    $sep = ", ";
                }

                $execute_array = $data;
            } else {
                $tuple_builder = array();

                foreach ($data as $key => $key_name) {
                    $keywords_builder.= "$sep$key_name";

                    foreach ($multiple as $index => $tuple) {
                        if (empty($tuple_builder[$index]))
                            $tuple_builder[$index] = "$sep:$key_name$index";
                        else
                            $tuple_builder[$index].= "$sep:$key_name$index";

                        if (empty($execute_array[$key_name.$index]))
                            $execute_array[$key_name.$index] = $tuple[$key];
                        else
                            $execute_array[$key_name.$index].= $tuple[$key];
                    }
                    $sep = ", ";
                }

                $tuple_builder = join("), (", $tuple_builder);
            }

            $r = $this->prepare_execute("INSERT INTO $this->etn_s$table$this->etn_e ($keywords_builder) VALUES ($tuple_builder)", $execute_array);

            if (!$id_return)
                return $r;

            return $this->select($table, "MAX($id_return)")->fetch()[0];
        }

        function update($table, $data, $where) {
            $change_builder = "";
            $sep = "";

            foreach ($data as $k => $v) {
                $change_builder.= "$sep$k = :$k";
                $sep = ", ";
            }

            $where_builder = $this->build_where($where, $data);

            return $this->prepare_execute("UPDATE $this->etn_s$table$this->etn_e SET $change_builder $where_builder", $data);
        }

        function delete($table, $where) {
            $execute_array = array();
            $where_builder = $this->build_where($where, $execute_array);

            return $this->prepare_execute("DELETE FROM $this->etn_s$table$this->etn_e WHERE $where_builder", $execute_array);
        }

        function list_enum($table, $column) { // TODO: refaire ; en vrai c'est bien fait, mais on peut se passer d'utiliser une RegEX pour ça...
            $sql = "SHOW COLUMNS FROM $this->etn_s$table$this->etn_e LIKE '$column'";
            $result = query($sql);
            $row = $result->fetch();
            $type = $row['Type'];
            preg_match('/enum\((.*)\)$/', $type, $matches);
            $vals = explode(',', str_replace("'", "", $matches[1]));
            return ($vals);
        }

    }
?>
